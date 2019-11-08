// Глобальные переменные интерфейса:
_page = null; // Буфер для последовательной сборки страницы. Создан для предотвращения мерцаний при переходах.
_pageOld = null; // Старая страница перед отображением новой.
_xmlLanguage = null; // jqXML с локализованными строками.
_xmlSettings = null; // jqXML с настройками системы.
_pageTag = null; // Тег с содержимым страницы.
_scriptsTag = null; // Тег со скриптами.

/**
	Получение текущего адреса.
	@return Строка с адресом.
*/
function getUrl() {
	return window.location.pathname + window.location.search + window.location.hash;
}

/**
	Получение UTF-8 адреса из шестнадцатиричного представления.
	@return UTF-8 адрес.
*/
function getDecodedUrl() {
	return decodeURI(getUrl());
}

/**
	Получение значение ключа по его идентификатору с учётом текущей локализации.
	@param keyId Идентификатор записи.
	@param keyIndex Номер одноименной записи.
	@return Локализованное сообщение.
*/
function getLocVal(keyId, keyIndex) {
	if (!keyIndex) keyIndex = 0; // Параметр по умолчанию.
    return _xmlLanguage.find("key[id='" + keyId + "']").eq(keyIndex).attr("val");
}

/**
	Получение параметра из текущих настроек.
	@param keyId Идентификатор параметра.
	@param keyIndex Номер одноименной записи.
	@return Значение параметра.
*/
function getSetting(keyId, keyIndex) {
	if (!keyIndex) keyIndex = 0; // Параметр по умолчанию.
    return _xmlSettings.find("key[id='" + keyId + "']").eq(keyIndex).attr("val");
}

/**
	Загрузка настроек в форму.
	@param jqForm Идентификатор формы.
	@param bValidate Флаг проверки правильности ввода.
*/
function settingsUpdate(jqForm, bValidate) {
	if (bValidate == true) {
		$("input, select", jqForm).each(function() {
			if (this.hasAttribute("name")) { // Обработка элементов с параметром name:
				if (this.tagName == "SELECT") {
					if (this.options[this.selectedIndex].value == getSetting(this.name)) this.parentNode.parentNode.setAttribute("class", ""); else this.parentNode.parentNode.setAttribute("class", "error"); // Двойной переход к родителю, т.к. select всегда находится в обертке.
				} else { // INPUT:
					if (this.type == "text" || (this.type == "radio" && this.checked == true)) {
						if (this.value == getSetting(this.name)) this.parentNode.setAttribute("class", ""); else this.parentNode.setAttribute("class", "error");
					}
				}
			}
		});
	} else {
		$("input, select", jqForm).each(function() {
			if (this.hasAttribute("name")) { // Обработка элементов с параметром name:
				if (this.tagName == "SELECT") {
					// Не понятно по поводу порядка изменения текущего значения select'а и текущего option'а. Порядок важен:
					//this.selectedIndex = $("option[value='" + getSetting(this.id) + "']", this).index();
					//alert(getSetting(this.name));
					//if (this.nextElementSibling) this.nextElementSibling.innerHTML = this.options[this.selectedIndex].text;
					$("option[value='" + getSetting(this.name) + "']", this).attr("selected", "selected");
				} else { // INPUT:
					if (this.type == "text") {
						this.value = getSetting(this.name);
					} else if (this.type == "radio" && this.value == getSetting(this.name)) {
						this.checked = true;
					} else if (this.type == "checkbox") {
						//this.checked = (getSetting(this.name) == "0" ? false : true);
						this.value = getSetting(this.name);
					}
				}
			}
		});
	}
}

/**
	Загрузка настроек в форму с подготовкой формы к использованию.
	@param jqForm Идентификатор формы.
*/
function settingsForm(jqForm) {
	return $.when(getSettings(), $.ajax({type:"POST", url:"/getLanguages", dataType:"xml"})).done(function(xmlSettings, xmlLanguages) {
		// Заполнение полей формы с настройками:
		settingsUpdate(jqForm, false);
		// Установка обработчиков формы:
		$("#btn-save", jqForm).click(function() {
			$.post("/setSettings", jqForm.serialize() + "&lastModified=" + Date.now(), function(htmlData) { // htmlData - ответ (в данном случае не используется) сервера на запрос.
				// Запрос отработал успешно. Проверка введенных полей проходит на сервере всегда, чтобы не делать повторный функционал в интерфейсе, просто, получим новые настройки и сравним:
				settingsUpdate(jqForm, true);
				// Отладка:
				//alert(jqForm.serialize() + "&lastModified=" + Date.now());
			});
			return false; // Для submit. Смена страницы запрещена. Атрибут action будет проигнорирован.
		});
	});
}

/**
	Макрос на получение xml с настройками.
	@return Deferred jQuery объект с настройками.
*/
function getSettings() {
	return $.ajax({type:"POST", cache:false, url:"/getSettings?lastModified=" + new Date().getTime(), dataType:"xml", success:function(xmlSettings, textStatus, jqXHR) {
		//delete _xmlSettings; // TODO: Необходимо разобраться со сборщиком мусора и оптимизировать весь код.
		_xmlSettings = $(xmlSettings); // Сохранение актуальных настроек. Если в некоторых частях программы настройки обновлять не требуется, то они берутся отсюда, в противном случае сначала обновляются до актуального состояния.
	}}); // Запрос актуальных настроек.
}

/**
	Получение актуальной локализации.
	@return Deferred jQuery объект с текущей локализацией.
*/
function getLanguage() {
	var deferredLanguage = $.Deferred(); // Переменная для реализации отложенного запроса.
	//$.when(getSettings()).then(function(xmlSettings) { // Загрузка настроек прошла успешно:
		$.when($.ajax({type:"GET", cache:true, url:"/getLanguage?lastModified=" + Date.now(), dataType:"xml"})).then(function(xmlLanguage, textStatus, jqXHR) {
			// Подготовка локализованных строк:
			delete _xmlLanguage;
			_xmlLanguage = $(xmlLanguage); // Сохраняем данные для локализации в глобальной переменной. Кэширование для быстрого повторного использования.
			// Сигнал завершения:
			deferredLanguage.resolve(xmlLanguage/*, xmlSettings*/); // Возвращаем данных для локализации и актуальных настроек.
		}, function() { // Загрузка локализованных строк закончилась неудачно:
			deferredLanguage.reject();
		});
	//}, function() { // Загрузка настроек не произошла по каким-либо причинам:
	//	deferredLanguage.reject();
	//});
	// После инициации первого запроса в родительскую функцию будет передана !resolved переменная:
	return deferredLanguage;
}

/**
	Получение локализованной DOM-структуры из строки с HTML-кодом.
	@param htmlString HTML-данные для преобразования в DOM.
	@return Объект jQuery с полученной DOM-структурой.
*/
function getLocDom(htmlString) {
	//var hack = /< *(script|iframe)|=[^'|"]*('|")[^'|"]*=[^'|"]*('|")|=(\w|'|"|:)*\([^\)]*\)/im; // Регулярное выражение для поиска внедрения кода. Ищет теги script, iframe и попытки запуска скрипта из параметров.
	//hack.test(value) ? $(this).text(value) : $(this).html(value); // Проверка на внедрение кода: text() игнорирует спец. символы.
	var result = $("<div>" + htmlString + "</div>"); // Узел языкового xml файла, подставляемое локализованное значение, атрибут для подстановки, html-данные с оберткой.
	// Обертка не учитывается при выводе содержимого с помощью метода html(), поэтому пришлось её использовать для предотвращения потери самих данных.	
	result.find("*[id], *[class]").each(function() { //result.find("*[id]").each(function() {
		var htmlNode = $(this); // Текущий элемент html страницы для локализации.
		
		// Формирование селектора на поиск подходящих фраз из языкового файла:
		var request = "key[id='" + htmlNode.attr("id") + "']";
		if (this.getAttribute("class")) {
			var params = htmlNode.attr("class").split(" ");
			for (var i = 0; i < params.length; i++) {
				request += ",key[id='" + params[i] + "']";
			}
			i = params = null;
		}

		// Локализация содержимого:
		$(request, _xmlLanguage).each(function() { // Данные для локализации текущего элемента.
			var xmlNode = $(this); // Запись xml для текущего элемента. В ней может содержаться текстовое значение для элемента, ссылка и т.п.
			var attr = xmlNode.attr("attr");
			var val = xmlNode.attr("val");
			// Перевод html элемента:
			if (val) { // Если такое значение имеется, производим замену.
			    if (attr) {
				    if (attr.substring(0, 2).toLowerCase() != "on")
				        htmlNode.attr(attr, val); // Добавление значений в атрибуты событий запрещено.
				} else {
					if (htmlNode[0].tagName == "INPUT") { // Перевод полей ввода:
						//htmlNode.val(val); // Поле ввода не содержит дочерних элементов, поэтому переводиться будет значение.
						htmlNode.attr("value", val); // Срабатывает точнее, чем val().
					} else { // Перевод элемента по умолчанию:
						var nodeContents = htmlNode[0].childNodes;
						// Производится замена только текстовых полей:
						if (nodeContents.length > 1) { // Элемент содержит дочерние элементы помимо текста, имеем возможность заменить только текстовые:
							for(i = 0; i < nodeContents.length; i++) {
								if (nodeContents[i].nodeType == 3) { // TODO: Not all browsers support constant TEXT_NODE = 3.
									nodeContents[i].nodeValue = val;
									break;
								}
							}
						} else { // Если элемент содержит только текст, имеется возможность вносить HTML элементы (ссылки например):
							htmlNode.html(val); // Проверка внедрения кода перенесена на сторону сервера.
							//htmlNode.text(value); // Метод text(value) экранирует спец.символы html для предотвращения внедрения кода.
						}
						nodeContents = null;
					}
				}
			}
			xmlNode = attr = val = null;
		});
		request = null;
	});
	return result;
}

/**
	Извлечение скриптов из jQuery объекта.
	@param jqObject jQuery object with scripts we need to extract.
	@return jQuery object with extracted scripts.
*/
function getScriptDomFrom(jqObject) {
	var result = $("<div></div>");
	jqObject.find("script").each(function() {
		result.append($(this)); // Перенос элемента в другое дерево. Удаление не требуется.
	});
	return result;
}

/**
	Устаревшая экспериментальная функция загрузки html контента.
	Строковые параметры через запятую ([адрес страницы, идентификатор местоположения], [адрес страницы, идентификатор местоположения], ...).
	@return Deferred jQuery object.
*/
function loadHtmlEx() {
	loadHtmlExDeferred = $.Deferred(), // Переменная для отслеживания загрузки данных.
		funcArgs = arguments, // Сохранение аргументов функции для использования во внутренних 
		i = 1, j = 0, // Индекс текущего аргумента.
		args = []; // Массив аргументов для метода $.when().
	// Выполнение запросов, переданных в функцию:
	do { // Выполнение всех AJAX запросов с записью deferred-объектов в массив:
		args.push($.ajax({type:"POST", cache:false, url:arguments[i], dataType:"html"}));
		i += 2;
	} while (i < arguments.length);
	
	// Заполнение полученными данными:
	$.when.apply($, args).then(function() { // Ожидание успешного выполнения запросов.
		locDom = null; // Локализованная DOM структура.
		jqScript = null;
		if (funcArgs.length < 3) { // Результат выполнения done() отличается по типу в зависимости от количества переданных переменных!
			locDom = getLocDom(arguments[0]); // Получение локализованного шаблона в виде дерева.
			jqScript = getScriptDomFrom(locDom);
			_page.find(funcArgs[0]).html(locDom.html());
			_scriptsTag.append(jqScript.html());
			delete locDom;
			delete jqScript;
		} else { // В противном случае в arguments массив:
			i = 0;
			j = 0;
			do {
				locDom = getLocDom(arguments[j][0]);
				jqScript = getScriptDomFrom(locDom);
				_page.find(funcArgs[i]).html(locDom.html());
				_scriptsTag.append(jqScript.html());
				delete locDom;
				delete jqScript;
				i += 2;
				j++;
			} while (i < funcArgs.length - 1); // -1 предотвращает обработку нечетного количества параметров.
		}
		// Загрузка завершена:
		loadHtmlExDeferred.resolve(); // TODO: Можно добавить полученные результаты в параметры для последующего использования.
	}, function() {
		loadHtmlExDeferred.reject();
	});
	return loadHtmlExDeferred;
}

/**
	Загрузка и локализация HTML-данных с последующей записью результата в буфер страницы.
	@param target String with jQuery selector or jQuery object.
	@param request HTTP request of HTML-data.
	@param append Optional writing method.
	@return jQuery deferred object.
*/
function loadHtml(target, request, append) {
	if (!append) append = false;
	loadHtmlDeferred = $.Deferred();
	$.when($.ajax({type:"POST", cache:true, url:request, dataType:"html"})).then(function(htmlData, textStatus, jqXHR) {
		// Обработка ответа "Not modified 304" отключена на стороне сервера (т.к. не поддерживается нормально браузерами):
		//if (jqXHR.status == 304) { // Данные получены из кэша:
		//	htmlData = jqXHR.responseText;
		//}
		// Формирование содержимого:
		targetObj = null; // Объект для записи.
		locDom = getLocDom(htmlData); // Получение локализованного шаблона в виде дерева.
		jqScript = getScriptDomFrom(locDom);
		// Определение контейнера для записи полученных данных:
		if (target.jquery) targetObj = target; else targetObj = _page.find(target);
		// Запись данных в соответствии с методом:
		if (append == true) targetObj.append(locDom.html()); else targetObj.html(locDom.html());
		// Запуск выполнения скриптов:
		_scriptsTag.append(jqScript.html());
		delete locDom;
		delete jqScript;
		loadHtmlDeferred.resolve();
	}, function() {
		loadHtmlDeferred.reject();
	});
	return loadHtmlDeferred;
}

/**
	Загрузка HTML-данных с тегами определенным образом в буфер страницы.
	@param target String with jQuery selector.
	@param request HTTP request of HTML-data.
	@return jQuery deferred object.
*/
function loadTags(target, request) {
	el = _page.find(target);
	wrap = $("<div id=\"" + el.attr("id") + "-wrap" + "\" class=\"tags\"></div>");
	el.parent().append(wrap);
	return loadHtml(wrap, request);
}

/**
	Макрос окончательной сборки страницы в соответствии с адресом.
	@return jQuery deferred object.
*/
function initPage() {
	// Mozilla Firefox и некоторые др. не поддерживает перенос <vide/> тега через DOM структуру _page, непривязанную к странице, поэтому используется своп:
	// Уничтожение старого кода страницы во вторую очередь:
	_scriptsTag.empty();
	_pageOld = _page; // Сохранение старой ссылки.
	// Создание буфера для фонового формирования загружаемой страницы:
	//_page = $("<div><div id=\"page-buffer\"></div></div>"); // Создание DOM объекта, не привязанного к документу. Все данные будут подгружаться в него. Двойной блок дает возможность выполнить метод loadHtml ниже.
	_page = $("<div id=\"page-buffer\"></div>");
	// Начало цепочки загрузки нового содержимого (раньше цепочка разбивалась на разные файлы, но дублирования всё равно не удалось устранить и по скорости выходило накладней):
	return loadHtml(_page, "/Views/Shared/layout.html").done(function() {
		switch (decodeURI(location.pathname).toLowerCase()) { // Разбор адреса без параметров.
			// Основная страница:
			case "/":
			loadHtml("#content", "/Views/home.html");
			break;

			// Основная страница с настройками:
			case "/settings": // Address localization example: case getLocVal("tag-stngs", 1):
			loadTags("#tag-stngs", "/Views/settingsTags.html").done(function() {
				loadHtml("#content", "/Views/settings.html");
			});
			break;

			// Members:
			case "/members":
			loadHtml("#content", "/Views/members.html");
			break;

			// Add or edit member:
			case "/member":
			loadTags("#tag-members", "/Views/memberTags.html").done(function() {
				loadHtml("#content", "/Views/member.html");
			});
			break;

			// Login:
			case "/login":
			loadTags("#tag-stngs", "/Views/settingsTags.html").done(function() {
				loadHtml("#content", "/Views/login.html");
			});
			break;

			// Fee details:
			case "/fee":
			loadTags("#tag-overview", "/Views/feeTags.html").done(function() {
				loadHtml("#content", "/Views/fee.html");
			});
			break;

			//// Настройки сети:
			//case getLocVal("tag-net", 1):
			//loadTags("#tag-stngs", "/template/settings/tags.html").done(function() {
			//	loadHtml("#content", "/template/settings/net.html");
			//});
			//break;

			//// Настройки безопасности:
			//case getLocVal("tag-security", 1):
			//loadTags("#tag-stngs", "/template/settings/tags.html").done(function() {
			//	loadTags("#tag-security", "/template/settings/security/tags.html").done(function() {
			//		loadHtml("#content", "/template/settings/security/index.html");
			//	});
			//});
			//break;

			//// Настройки HTTPS безопасности:
			//case getLocVal("tag-https", 1):
			//loadTags("#tag-stngs", "/template/settings/tags.html").done(function() {
			//	loadTags("#tag-security", "/template/settings/security/tags.html").done(function() {
			//		loadHtml("#content", "/template/settings/security/https.html");
			//	});
			//});
			//break;
			
			//// Настройки дополнительные:
			//case getLocVal("tag-other", 1):
			//loadTags("#tag-stngs", "/template/settings/tags.html").done(function() {
			//	loadHtml("#content", "/template/settings/other.html");
			//});
			//break;

			// Неизвестный ресурс:
			default: loadHtml("#content", "/Views/Shared/error.404.html"); break;
		}
	});
}

/**
	Изменение поведения некоторых компонентов ввода.
*/
function labelOnClick(event, forObj) {
	if (forObj.type == "checkbox") { // Измененное поведение:
		event.preventDefault();
		forObj.value = (forObj.value == "0" ? "1" : "0");
	}
	forObj.focus(); // Safari and chrome.
}

/**
	Выполнить переход на адрес ссылки.
*/
function goToTargetHref(e, href) {
	if (href == undefined)
		href = e.target.getAttribute("href");
	window.history.pushState(window.history.length + 1, document.title, href); // Короткий адрес, на который будет совершен переход после нажатия на ссылку. А e.target.href - полный адрес для переход.
	initPage(); // Выполняем переход по короткому новому адресу, переданному ранее объекту history.
	e.preventDefault(); // Предотвращение стандартного перехода браузера по ссылке href.
}

/**
	Отображение буфера со сформированной страницей на экран.
	TODO: Модифицировать код по возможности для многоразового использования в одной итерации.
*/
function showPage() {
	// Модификация некоторых элементов страницы:
	// Преобразование содержимого страницы для отображения стилизованных radio и check боксов:
	$("label[for], select", _page).each(function() {
		htmlNode = $(this); // Текущий элемент html страницы.
		if (htmlNode.is("label")) {
			htmlNode.html("<span><span/></span>" + htmlNode.text());
			// Исправления для некоторых браузеров:
			htmlNode.attr("onclick", "labelOnClick(event, document.getElementById('" + this.getAttribute("for") + "'));");
		} else { //} else if (htmlNode.is("select")) {
			htmlNode.wrap("<div class=\"select-wrap\"/>");
			htmlNode.parent().append("<div>" + $("option:selected", htmlNode).text() + "</div>");
			//htmlNode.attr("onchange", "$(this).next().text($('option:selected', this).text());");
			htmlNode.attr("onchange", "this.nextElementSibling.innerHTML = this.options[this.selectedIndex].text;");
		}
	});

	// Перевести в начало новой страницы:
	// TODO: Данный функционал не используется, т.к. в некоторых случаях не требуется делать перевод.
	//if (history.state == window.history.length) { // Если страница в истории действительно новая.
	//	window.scrollTo(0, 0);
	//}

	// Замена данных страницы новыми буферизированными (для предотвращения мерцания):
	_pageTag.html(_page);

	// Если система поддерживает HTML 5 history, производится добавление обработчиков:
	if (window.history.pushState && window.history.replaceState) {
		//$("a:not([target])").each(function() { // Перебор внутренних ссылок страницы:
		$("a[target=_self]").each(function() { // Только специальные ссылки будут поддерживать AJAX. Остальные будут реагировать как обычно.
			$(this).off("click").on("click", goToTargetHref);
		});
	}
	// Вызов события об окончании вывода страницы на экран:
	_page.trigger("onshow"); // http://api.jquery.com/trigger.

	// Уничтожение старой страницы (по идее после переноса DOM структуры на место старой, последняя должна удаляться из памяти, но на всякий случай удалим вручную):
	if (_pageOld) {
		_pageOld.off(); // Уничтожение имеющихся обработчиков событий.
		_pageOld.remove(); // Уничтожение структуры и событий.
		delete _pageOld; // Уничтожение объекта (предполагается, что ссылка единственная).
		_pageOld = null; // Сброс указателя.
	}
}

/**
	Подготовка индексной страницы, с которой начинается формирование любой из частей интерфейса.
*/
function initIndex() {
	// Сначала происходит кэширование данных для локализации (при смене языка необходимо повторное кэширование):
	$.when(getLanguage()).done(function(xmlLanguage, xmlSettings) { // Первый раз приходится выполнить лишнюю загрузку языкового файла. Т.к. в свитче к моменту выбора страницы для загрузки уже необходимы локализованные данные.
		_pageTag = $("#page");
		_scriptsTag = $("#scripts");
		
		// Формирование страницы:
		if (window.history.replaceState) window.history.replaceState("", document.title); // Now history.state will not be null for the event onpopstate handler.
		initPage(); // Загрузка страницы по текущему адресу.
		// Обработчик перемещения по истории. Вызов происходит только при использовании кнопок навигации в браузере:
		window.onpopstate = function(e) { // При начальной загрузке страницы не срабатывает.
			//alert(decodeURI(e.location || document.location)); // Полный адрес.
			if (window.history.state != null) initPage(); // Событие спровоцировал пользователь. Производится переход по полученной ссылке.
		}
	});
}

/**
	Преобразование XML в строку.
*/
function serializeXML(xmldom) {
	if (typeof XMLSerializer != "undefined") {
		return (new XMLSerializer()).serializeToString(xmldom);
	} else if (typeof xmldom.xml != "undefined") {
		return xmldom.xml;
	} else {
		throw new Error("Could not serialize XML DOM.");
	}
}

// Получение выделенного текста:
function getSelectedText() {
	if (window.getSelection) {
		return window.getSelection().toString();
	} else if (document.selection && document.selection.type != "Control") {
		return document.selection.createRange().text;
	}
	return null;
}

// Выделение в поле части текста:
function selectText(element, start, length) {
	if (element.createTextRange) {
		range = element.createTextRange();
		range.collapse(true);
		range.moveStart("character", start);
		range.moveEnd("character", length);
		range.select();
		delete range;
	} else if (element.setSelectionRange) {
		element.setSelectionRange(start, start + length);
	} else if( element.selectionStart ) {
		element.selectionStart = start;
		element.selectionEnd = start + length;
	}
}

// Сортировка элементов тега <select>:
function sortSelect(selectId) {
	var sortedVals = $.makeArray($(selectId + ' option')).sort(function(a, b) {
		return $(a).text() > $(b).text() ? 1 : $(a).text() < $(b).text() ? -1 : 0;
	});
	$(selectId).empty().html(sortedVals);
}

// Сортировка элементов тега <select> по значению:
function sortSelectByValue(selectId) {
	var sortedVals = $.makeArray($(selectId + ' option')).sort(function(a,b) {
		return $(a).attr('value') > $(b).attr('value') ? 1 : $(a).attr('value') < $(b).attr('value') ? -1 : 0;
	});
	$(selectId).empty().html(sortedVals);
}

// SHA-256:
function sha256(msg) {

	// SHA256 logical functions:
	function rotateRight(n,x) {
		return ((x >>> n) | (x << (32 - n)));
	}
	function choice(x,y,z) {
		return ((x & y) ^ (~x & z));
	}
	function majority(x,y,z) {
		return ((x & y) ^ (x & z) ^ (y & z));
	}
	function sha256_Sigma0(x) {
		return (rotateRight(2, x) ^ rotateRight(13, x) ^ rotateRight(22, x));
	}
	function sha256_Sigma1(x) {
		return (rotateRight(6, x) ^ rotateRight(11, x) ^ rotateRight(25, x));
	}
	function sha256_sigma0(x) {
		return (rotateRight(7, x) ^ rotateRight(18, x) ^ (x >>> 3));
	}
	function sha256_sigma1(x) {
		return (rotateRight(17, x) ^ rotateRight(19, x) ^ (x >>> 10));
	}
	function sha256_expand(W, j) {
		return (W[j&0x0f] += sha256_sigma1(W[(j+14)&0x0f]) + W[(j+9)&0x0f] + sha256_sigma0(W[(j+1)&0x0f]));
	}

	// Hash constant words K:
	var K256 = new Array(
		0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
		0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
		0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
		0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
		0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
		0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
		0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
		0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
		0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
		0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
		0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
		0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
		0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
		0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
		0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
		0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
	);

	// global arrays:
	var ihash, count, buffer;
	var sha256_hex_digits = "0123456789abcdef";

	// Add 32-bit integers with 16-bit operations (bug in some JS-interpreters: overflow):
	function safe_add(x, y) {
		var lsw = (x & 0xffff) + (y & 0xffff);
		var msw = (x >> 16) + (y >> 16) + (lsw >> 16);
		return (msw << 16) | (lsw & 0xffff);
	}

	// Initialise the SHA256 computation:
	function sha256_init() {
		ihash = new Array(8);
		count = new Array(2);
		buffer = new Array(64);
		count[0] = count[1] = 0;
		ihash[0] = 0x6a09e667;
		ihash[1] = 0xbb67ae85;
		ihash[2] = 0x3c6ef372;
		ihash[3] = 0xa54ff53a;
		ihash[4] = 0x510e527f;
		ihash[5] = 0x9b05688c;
		ihash[6] = 0x1f83d9ab;
		ihash[7] = 0x5be0cd19;
	}

	// Transform a 512-bit message block:
	function sha256_transform() {
		var a, b, c, d, e, f, g, h, T1, T2;
		var W = new Array(16);

		// Initialize registers with the previous intermediate value:
		a = ihash[0];
		b = ihash[1];
		c = ihash[2];
		d = ihash[3];
		e = ihash[4];
		f = ihash[5];
		g = ihash[6];
		h = ihash[7];

		// Make 32-bit words:
		for(var i=0; i<16; i++)
			W[i] = ((buffer[(i<<2)+3]) | (buffer[(i<<2)+2] << 8) | (buffer[(i<<2)+1] << 16) | (buffer[i<<2] << 24));

		for(var j=0; j<64; j++) {
			T1 = h + sha256_Sigma1(e) + choice(e, f, g) + K256[j];
			if(j < 16) T1 += W[j];
			else T1 += sha256_expand(W, j);
			T2 = sha256_Sigma0(a) + majority(a, b, c);
			h = g;
			g = f;
			f = e;
			e = safe_add(d, T1);
			d = c;
			c = b;
			b = a;
			a = safe_add(T1, T2);
		}

		// Compute the current intermediate hash value:
		ihash[0] += a;
		ihash[1] += b;
		ihash[2] += c;
		ihash[3] += d;
		ihash[4] += e;
		ihash[5] += f;
		ihash[6] += g;
		ihash[7] += h;
	}

	// Read the next chunk of data and update the SHA256 computation:
	function sha256_update(data, inputLen) {
		var i, index, curpos = 0;
		// Compute number of bytes mod 64:
		index = ((count[0] >> 3) & 0x3f);
			var remainder = (inputLen & 0x3f);

		// Update number of bits:
		if ((count[0] += (inputLen << 3)) < (inputLen << 3)) count[1]++;
		count[1] += (inputLen >> 29);

		// Transform as many times as possible:
		for(i=0; i+63<inputLen; i+=64) {
					for(var j=index; j<64; j++)
				buffer[j] = data.charCodeAt(curpos++);
			sha256_transform();
			index = 0;
		}

		// Buffer remaining input:
		for(var j=0; j<remainder; j++)
			buffer[j] = data.charCodeAt(curpos++);
	}

	// Finish the computation by operations such as padding:
	function sha256_final() {
		var index = ((count[0] >> 3) & 0x3f);
			buffer[index++] = 0x80;
			if(index <= 56) {
			for(var i=index; i<56; i++)
				buffer[i] = 0;
			} else {
			for(var i=index; i<64; i++)
				buffer[i] = 0;
					sha256_transform();
					for(var i=0; i<56; i++)
				buffer[i] = 0;
		}
		buffer[56] = (count[1] >>> 24) & 0xff;
		buffer[57] = (count[1] >>> 16) & 0xff;
		buffer[58] = (count[1] >>> 8) & 0xff;
		buffer[59] = count[1] & 0xff;
		buffer[60] = (count[0] >>> 24) & 0xff;
		buffer[61] = (count[0] >>> 16) & 0xff;
		buffer[62] = (count[0] >>> 8) & 0xff;
		buffer[63] = count[0] & 0xff;
		sha256_transform();
	}

	// Split the internal hash values into an array of bytes:
	function sha256_encode_bytes() {
		var j=0;
		var output = new Array(32);
		for(var i=0; i<8; i++) {
			output[j++] = ((ihash[i] >>> 24) & 0xff);
			output[j++] = ((ihash[i] >>> 16) & 0xff);
			output[j++] = ((ihash[i] >>> 8) & 0xff);
			output[j++] = (ihash[i] & 0xff);
		}
		return output;
	}

	// Get the internal hash as a hex string:
	function sha256_encode_hex() {
		var output = new String();
		for(var i=0; i<8; i++) {
			for(var j=28; j>=0; j-=4)
				output += sha256_hex_digits.charAt((ihash[i] >>> j) & 0x0f);
		}
		return output;
	}
	
	// To URL byte format:
	function sha256_encode_url() {
		var output = new String();
		for(var i=0; i<8; i++) {
			for(var j=28; j>=0; j-=8) {
				output += "%" + sha256_hex_digits.charAt((ihash[i] >>> j) & 0x0f) + sha256_hex_digits.charAt((ihash[i] >>> (j-4)) & 0x0f);
			}
		}
		return output;
	}

	// Main function: returns a hex string representing the SHA256 value of the given data:
	function sha256_digest(data) {
		sha256_init();
		sha256_update(data, data.length);
		sha256_final();
		return sha256_encode_url();
	}

	return sha256_digest(msg);
}

// Отладочная информация об объекте.
function showObject(obj) {
	var result = "";
	for (var i in obj) // обращение к свойствам объекта по индексу
	result += i + " = " + obj[i] + "\n";
	return result;
}

/**
	Функция получения параметров из адресной строки.
	
	// Get object of URL parameters
	var allVars = $.getUrlVars();
	// Getting URL var by its nam
	var byName = $.getUrlVar('name');
*/
$.extend({
	getUrlVars: function() {
		var vars = [], hash;
		var hashes = window.location.href.slice(window.location.href.indexOf('?') + 1).split('&');
		for (var i = 0; i < hashes.length; i++)
		{
			hash = hashes[i].split('=');
			vars.push(hash[0]);
			vars[hash[0]] = hash[1];
		}
		return vars;
	},
		getUrlVar: function(name) {
			return $.getUrlVars()[name];
	}
});

// Additional JS variant:
function getParameterByName(name, url) {
	if (!url) url = window.location.href;
	name = name.replace(/[\[\]]/g, '\\$&');
	var regex = new RegExp('[?&]' + name + '(=([^&#]*)|&|#|$)'),
		results = regex.exec(url);
	if (!results) return null;
	if (!results[2]) return '';
	return decodeURIComponent(results[2].replace(/\+/g, ' '));
}

// Escapes text:
var escapeChars = {
	'<': 'lt',
	'>': 'gt',
	'"': 'quot',
	'&': 'amp',
	'\'': '#39'
}, regexString = '[';
for (var key in escapeChars) {
	regexString += key;
}
regexString += ']';

var regex = new RegExp(regexString, 'g');

function escapeHtml(str) {
	var result = str.replace(regex, function (m) {
		return '&' + escapeChars[m] + ';';
	});
	return result.replace(/\r?\n/g, "&#13;&#10;");
};

function isEmptyString(str) {
	return (str == null || str.trim() === '');
}

/*function scrollTop() {
	$("html:not(:animated)" + (!$.browser.opera ? ",body:not(:animated)" : "")).animate({scrollTop:0}, 300);
	return false;
}

function scrollBottom() {
	$("html:not(:animated)" + (!$.browser.opera ? ",body:not(:animated)" : "")).animate({scrollTop:$(document).height()}, 300);
	return false;
}*/

/*
function initControls() {
    $("label[for]").each(function() {
        $(this).html("<span><span/></span>" + $(this).html());
        $(this).click(function() { // Safari.
            $("#" + $(this).attr("for")).focus(); // Chrome.
        });
    });
    
    // Селекты:
    $(".iks").ikSelect({
        autoWidth: false,
        ddFullWidth: false
    });
}

function showContent() {
    // Установка событий на checkbox и radio элементы. Для правильной работы этих элементов на смартфонах.
    $("input[type='radio'], input[type='checkbox']").each(function(){
        var rcElement = $(this);
        $("label[for='" + $(this).attr("id") + "']").click(function(){
            rcElement.focus(); // Устанавка фокуса для стилизованных элементов (браузер не переводит фокус).
            //var current = $("#" + $(this).attr("for"));
            //current.attr("checked", !current.attr("checked"));
        });
    });
    //window["ajaxed"] = true;
//setTimeout(function() {
    // Отображение результата:
    //$("#bodyLoading").hide();
    
    //initControls();
    
    $("#body-content-holder").css("display", "block");
    $("#base").show();
//}, 500);
}
*/
